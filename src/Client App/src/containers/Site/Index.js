import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useDispatch, useSelector } from 'react-redux';
import { Col, Row, Spinner, Button } from 'react-bootstrap';
import { getSite } from '../../store/site-actions';

import ReactDataGrid from '@inovua/reactdatagrid-community';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faCheck, faTimes, faEdit, faTrash } from '@fortawesome/free-solid-svg-icons'

import '../../App.css';
import EditSite from './EditSite';
import AddSite from './AddSite';
import DeleteSite from './DeleteSite';

import _ from 'lodash';

const gridStyle = { minHeight: 485, marginTop: 10 };


const Site = () => {

    const columns = [

        {
            name: 'name', defaultFlex: 1, header: 'Name',
            render: ({ data }) => <Link to={`/site/${data.id}`}>{data.name}</Link>

        },
        { name: 'scoreValue', defaultFlex: 1, header: 'Average Score' },
        {
            name: 'lastSeen', defaultFlex: 1, header: 'Last Seen',
            render: ({ value }) => {
                let date, time;
                [date, time] = value.split('T')
                return (
                    <>
                        <span>{date}</span>
                    </>
                )
            }
        },
        {
            name: 'isActive', defaultFlex: 0.5, header: 'Active', className: "text-center",
            render: ({ value }) => <div className="text-center"> {value ?
                <FontAwesomeIcon icon={faCheck} className="text-success" />
                : <FontAwesomeIcon icon={faTimes} className="text-danger" />} </div>
        },
        {
            header: 'Edit',
            name: 'id',
            defaultFlex: 0.5,
            className: "text-center",
            render: ({ data }) => {
                return (
                    <div className="text-center">
                        <Button variant="dark" size="sm" block onClick={() => editHandler(data)}>
                            <FontAwesomeIcon icon={faEdit} className="text-light" />
                        </Button>
                    </div>
                )
            }
        },
        {

            header: 'Delete',
            defaultFlex: 0.5,
            className: "text-center",
            render: ({ data }) => {
                return (
                    <div className="text-center">
                        <Button variant="dark" size="sm" block onClick={() => { deleteHandler(data) }}>
                            <FontAwesomeIcon icon={faTrash} className="text-light" />
                        </Button>
                    </div>
                )
            }

        }

    ];

    const { isLoading, siteInfo } = useSelector(state => state.site);
    const { user } = useSelector(state => state.auth);
    const [skip, setSkip] = useState(0);

    const [showEditModal, setShowEditModal] = useState(false);
    const [showAddModal, setShowAddModal] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);

    const [editData, setEditData] = useState({});
    const [deleteData, setDeleteData] = useState({});

    const dispatch = useDispatch();

    const addHandler = () => {

        setShowAddModal(true);
    }

    const editHandler = (data) => {
        console.log(data)
        setEditData(data);
        setShowEditModal(true);
    }

    const deleteHandler = (data) => {

        setDeleteData(data);
        setShowDeleteModal(true);
    }

    const addModalCloseHandler = () => {
        setShowAddModal(false);
    }

    const editModalCloseHandler = () => {
        setShowEditModal(false);
    }

    const deleteModalCloseHandler = () => {
        setShowDeleteModal(false);
    }

    const skipChangeHandler = (skipValue) => {

        const pageNumberValue = (skipValue / 10) + 1;
        dispatch(getSite({ pageNumber: pageNumberValue, pageSize: 10 }))
        setSkip(skipValue);
    }

    useEffect(() => {

        dispatch(getSite(user.companyId))

    }, [dispatch, user])

    return (
        <div>
            <Row className="mt-2">
                <Col md={10}>
                    <h1 className="display-4"> Site </h1>
                </Col>
                <Col md={2}>
                    <Button type="button" variant="outline-dark" className='mt-4 float-right' onClick={() => { addHandler() }}>Add Site</Button>
                </Col>
            </Row>
            <hr />
            <AddSite show={showAddModal} onClose={addModalCloseHandler} skip={skip} />
            {showEditModal ? <EditSite show={showEditModal} onClose={editModalCloseHandler} data={editData} /> : ''}
            {showDeleteModal ? <DeleteSite show={showDeleteModal} onClose={deleteModalCloseHandler} data={deleteData} /> : ''}
            <Row>
                {
                    !isLoading ?
                        !_.isEmpty(siteInfo)
                            ? <ReactDataGrid
                                idProperty="id"
                                style={gridStyle}
                                columns={columns}
                                pagination
                                skip={skip}
                                sortable={false}
                                showColumnMenuTool={false}
                                onSkipChange={skipChangeHandler}
                                pageSizes={false}
                                theme="default-light"
                                dataSource={new Promise(function (resolve, reject) {
                                    resolve(siteInfo)
                                })}
                                defaultLimit={10}
                            />

                            : <Spinner animation="border spinner" />
                        : <Spinner animation="border spinner" />
                }
            </Row>
        </div>
    )



};

export default Site;