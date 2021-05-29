import React from 'react';
import { deleteCompany } from '../../store/company-actions';

import { useDispatch } from "react-redux";
import DeleteModal from '../../components/DeleteModal/DeleteModal';

const DeleteCompany = ({ show, onClose, data }) => {

    const dispatch = useDispatch();

    const deleteCompanyHandler = () => {
        dispatch(deleteCompany(data));
        onClose();
    }

    return (
        <DeleteModal show={show} onClose={onClose} data={data} handler={deleteCompanyHandler}/>
    )

}

export default DeleteCompany;