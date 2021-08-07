import React from 'react';
import { Modal } from 'react-bootstrap';
import TaskForm from './TaskForm';

const EditCompany = ({ show, onClose, data }) => {

    return (
        <Modal show={show} onHide={onClose} centered>    
                {<TaskForm isEdit={true} data={data} onClose={onClose}/>}
        </Modal>
    )

}

export default EditCompany;